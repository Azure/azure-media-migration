import { resourceGroupName } from "./lib/config";
import { LogAnalyticsWorkspace } from "./resources/logAnalyticsWorkspace";
import { ContainerRegistry } from "./resources/containerRegistry";
import { Servicebus } from "./resources/servicebus";
import { ContainerAppJob } from "./resources/containerAppJob";

const containerRegistry = new ContainerRegistry(resourceGroupName);

const logAnalyticsWorkspace = new LogAnalyticsWorkspace(resourceGroupName);
const logAnalyticsWorkspaceSharedKey = logAnalyticsWorkspace.getPrimarySharedKey(resourceGroupName);

const servicebus = new Servicebus(resourceGroupName);

const containerAppJob = new ContainerAppJob(
  resourceGroupName,
  servicebus,
  containerRegistry.containerRegistry,
  logAnalyticsWorkspace.workspace,
  logAnalyticsWorkspaceSharedKey,
);

export const servicebusNamespaceName = servicebus.namespace.name;
export const servicebusQueueName = servicebus.queue.name;
export const containerAppJobName = containerAppJob.job.name;
