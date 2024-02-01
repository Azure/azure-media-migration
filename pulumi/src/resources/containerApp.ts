import * as pulumi from "@pulumi/pulumi";
import * as azure_native from "@pulumi/azure-native";
import * as app from "@pulumi/azure-native/app";
import * as operationalinsights from "@pulumi/azure-native/operationalinsights";
import { authorization, managedidentity, containerregistry } from "@pulumi/azure-native";
import * as Constants from "../lib/constants";
import { resourceName } from "../lib/utils";
import { Servicebus } from "./servicebus";

export class ContainerApp {
  public environment: app.ManagedEnvironment;
  public containerApp: app.ContainerApp;
  private apiManagedIdentity: managedidentity.UserAssignedIdentity;
  private roleAssignmentQueue: authorization.RoleAssignment;
  private roleAssignmentRegistry: authorization.RoleAssignment;

  constructor(
    resourceGroupName: string,
    servicebus: Servicebus,
    containerRegistry: pulumi.Output<containerregistry.GetRegistryResult>,
    logAnalyticsWorkspace: operationalinsights.Workspace,
    logAnalyticsWorkspaceSharedKey: pulumi.Output<string>,
  ) {
    this.environment = new app.ManagedEnvironment("api-main", {
      resourceGroupName,
      environmentName: resourceName("api-e"),
      appLogsConfiguration: {
        destination: "log-analytics",
        logAnalyticsConfiguration: {
          customerId: logAnalyticsWorkspace.customerId,
          sharedKey: logAnalyticsWorkspaceSharedKey,
        }
      }
    });

    this.apiManagedIdentity = new managedidentity.UserAssignedIdentity("api-identity", {
      resourceGroupName,
      resourceName: resourceName("api-id")
    });

    this.containerApp = new app.ContainerApp("api-containerApp", {
      containerAppName: resourceName("api"),
      environmentId: this.environment.id,
      location: "East US",
      resourceGroupName: resourceGroupName,
      template: {
        containers: [{
          image: `${Constants.ContainerRegistryServer}/${Constants.ContainerAppApiImageName}:${Constants.ContainerAppApiImageTag}`,
          name: Constants.ContainerAppApiImageName,
          resources: {
            cpu: 0.5,
            memory: "1Gi",
          },
          env: [
            {
              name: "SERVICEBUS_NAMESPACE",
              value: servicebus.namespace.name
            },
            {
              name: "SERVICEBUS_QUEUE",
              value: servicebus.queue.name
            },
          ],
        }],
        scale: {
          maxReplicas: 5,
          minReplicas: 1,
          rules: [{
            custom: {
              metadata: {
                concurrentRequests: "50",
              },
              type: "http",
            },
            name: "httpscalingrule",
          }],
        },
      },
      configuration: {
        ingress: {
          external: true,
          targetPort: 8080,
          traffic: [{
              weight: 100,
              latestRevision: true
          }],
        },
        registries: [{
          server: Constants.ContainerRegistryServer,
          identity: this.apiManagedIdentity.id,
        }],
      },
      workloadProfileType: "GeneralPurpose",
      identity: {
        type: "UserAssigned",
        userAssignedIdentities: [
          this.apiManagedIdentity.id,
        ],
      }
    });

    this.roleAssignmentQueue = new authorization.RoleAssignment("sendmessagestoqueue", {
      scope: servicebus.queue.id,
      roleDefinitionId: `/subscriptions/${Constants.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/69a216fc-b8fb-44d8-bc22-1f3c2cd27a39`,
      principalId: this.apiManagedIdentity.principalId,
      principalType: "ServicePrincipal",
    });

    this.roleAssignmentRegistry = new authorization.RoleAssignment("apicontainerapp", {
      scope: containerRegistry.id,
      roleDefinitionId: `/subscriptions/${Constants.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/7f951dda-4ed3-4680-a7ca-43fe172d538d`,
      principalId: this.apiManagedIdentity.principalId,
      principalType: "ServicePrincipal",
    });
  }
}
