import * as pulumi from "@pulumi/pulumi";
import * as azure_native from "@pulumi/azure-native";
import * as app from "@pulumi/azure-native/app/v20230801preview";
import * as operationalinsights from "@pulumi/azure-native/operationalinsights";
import { authorization, managedidentity, containerregistry } from "@pulumi/azure-native";
import * as Constants from "../lib/constants";
import { resourceName } from "../lib/utils";
import { Servicebus } from "./servicebus";

export class ContainerApp {
  public environment: app.ManagedEnvironment;
  public job: app.Job;
  private jobManagedIdentity: managedidentity.UserAssignedIdentity;
  private roleAssignmentQueue: authorization.RoleAssignment;
  private roleAssignmentRegistry: authorization.RoleAssignment;

  constructor(
    resourceGroupName: string,
    servicebus: Servicebus,
    containerRegistry: pulumi.Output<containerregistry.GetRegistryResult>,
    logAnalyticsWorkspace: operationalinsights.Workspace,
    logAnalyticsWorkspaceSharedKey: pulumi.Output<string>,
  ) {
    this.environment = new app.ManagedEnvironment("main", {
      resourceGroupName,
      environmentName: resourceName("cae"),
      workloadProfiles: [
        {
          name: "Consumption",
          workloadProfileType: "Consumption",
        }
      ],
      appLogsConfiguration: {
        destination: "log-analytics",
        logAnalyticsConfiguration: {
          customerId: logAnalyticsWorkspace.customerId,
          sharedKey: logAnalyticsWorkspaceSharedKey,
        }
      }
    });

    this.jobManagedIdentity = new managedidentity.UserAssignedIdentity("job", {
      resourceGroupName,
      resourceName: resourceName("job-id")
    });

    this.job = new app.Job("job", {
      resourceGroupName,
      jobName: resourceName("ca-job"),
      environmentId: this.environment.id,
      workloadProfileName: "Consumption",
      configuration: {
        triggerType: "Event",
        eventTriggerConfig: {
          parallelism: 1,
          replicaCompletionCount: 1,
          scale: {
            maxExecutions: 5,
            minExecutions: 0,
            pollingInterval: 60,
            rules: [{
              metadata: {
                queueName: servicebus.queue.name,
                namespace: servicebus.namespace.name,
                messageCount: "1"
              },
              name: "servicebuscalingrule",
              type: "azure-servicebus",
              auth: [{
                secretRef: "connection-string-secret",
                triggerParameter: "connection"
              }],
            }],
          },
        },
        replicaRetryLimit: 1,
        replicaTimeout: 900, // 1800
        secrets: [{
          name: "connection-string-secret",
          value: servicebus.getPrimaryConnectionString()
        }],
        registries: [{
          server: Constants.ContainerRegistryServer,
          identity: this.jobManagedIdentity.id,
        }],
      },
      template: {
        containers: [{
          image: `${Constants.ContainerRegistryServer}/${Constants.ContainerAppJobImageName}:${Constants.ContainerAppJobImageTag}`,
          name: Constants.ContainerAppJobImageName,
          resources: {
            cpu: 0.5,
            memory: "1Gi"
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
            {
              name: "AZURE_CLIENT_ID",
              value: this.jobManagedIdentity.clientId
            },
          ],
        }],
      },
      identity: {
        type: "UserAssigned",
        userAssignedIdentities: [
          this.jobManagedIdentity.id,
        ],
      },
    });

    this.roleAssignmentQueue = new authorization.RoleAssignment("receivemessagesfromqueue", {
      scope: servicebus.queue.id,
      roleDefinitionId: `/subscriptions/${Constants.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0`,
      principalId: this.jobManagedIdentity.principalId,
      principalType: "ServicePrincipal",
    });

    this.roleAssignmentRegistry = new authorization.RoleAssignment("jobcontainer", {
      scope: containerRegistry.id,
      roleDefinitionId: `/subscriptions/${Constants.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions/7f951dda-4ed3-4680-a7ca-43fe172d538d`,
      principalId: this.jobManagedIdentity.principalId,
      principalType: "ServicePrincipal",
    });
  }
}
