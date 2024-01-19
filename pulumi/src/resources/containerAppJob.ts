import * as app from "@pulumi/azure-native/app";
import { getEnv, resourceGroupName } from "../lib/config";
import { servicebusNamespace, servicebusQueue, servicebusQueueOutput } from "./servicebus";

const containerAppJobImage = {
  name: getEnv("CONTAINER_APP_JOB_IMAGE_NAME"),
  tag: getEnv("CONTAINER_APP_JOB_IMAGE_TAG"),
  repository: getEnv("CONTAINER_REGISTRY"),
};

export const environment = new app.ManagedEnvironment("main", {
  resourceGroupName,
  environmentName: "ams-migration-cae",
  kind: "serverless",
  sku: {
    name: app.SkuName.Consumption
  }
});

export const job = new app.Job("job", {
  resourceGroupName,
  jobName: "ams-migration-ca-job",
  environmentId: environment.id,
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
            queueName: servicebusQueue.name,
            namespace: servicebusNamespace.name,
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
      value: servicebusQueueOutput.name
    }]
  },
  template: {
    containers: [{
      image: `${containerAppJobImage.repository}/${containerAppJobImage.name}:${containerAppJobImage.tag}`,
      name: containerAppJobImage.name,
    }],
  },
});
