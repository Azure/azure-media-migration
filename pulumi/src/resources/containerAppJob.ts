import * as app from "@pulumi/azure-native/app/v20230801preview";
import { authorization } from "@pulumi/azure-native";
import * as Environment from "../lib/env_vars";
import { resourceName } from "../lib/utils";
import { Servicebus } from "./servicebus";

export class ContainerAppJob {
  public environment: app.ManagedEnvironment;
  public job: app.Job;
  private roleAssignment: authorization.RoleAssignment;

  constructor(resourceGroupName: string, servicebus: Servicebus) {
    this.environment = new app.ManagedEnvironment("main", {
      resourceGroupName,
      environmentName: resourceName("cae"),
      workloadProfiles: [
        {
          name: "Consumption",
          workloadProfileType: "Consumption",
        }
      ]
    });

    this.job = new app.Job("job", {
      resourceGroupName,
      jobName: resourceName("ca-job"),
      environmentId: this.environment.id,
      configuration: {
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
                messageCount: 1
              },
              name: "servicebuscalingrule",
              type: "azure-servicebus",
              auth: [{
                secretRef: "connection-string-secret"
              }]
            }],
          },
        },
        replicaRetryLimit: 1,
        replicaTimeout: 1800,
        triggerType: "Event",
        secrets: [{
          name: "connection-string-secret",
          value: servicebus.getPrimaryConnectionString()
        }]
      },
      template: {
        containers: [{
          image: `${Environment.ContainerRegistry}/${Environment.ContainerAppJobImageName}:${Environment.ContainerAppJobImageTag}`,
          name: Environment.ContainerAppJobImageName,
          resources: {
            cpu: 1,
            memory: "2Gb"
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
      },
      identity: {
        type: "SystemAssigned"
      }
    });

    this.roleAssignment = new authorization.RoleAssignment("receivemessagesfromqueue", {
      scope: servicebus.queue.id,
      roleDefinitionId: "/providers/Microsoft.Authorization/roleDefinitions/4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0",
      principalId: this.job.identity.apply(i => i!.principalId)
    });
  }
}
