import { ContainerAppJob } from "./resources/containerAppJob";
import { resourceGroupName } from "./lib/config";
import { ContainerRegistry } from "./resources/containerRegistry";
import { Servicebus } from "./resources/servicebus";

const containerRegistry = new ContainerRegistry(resourceGroupName);

const servicebus = new Servicebus(resourceGroupName);

const containerAppJob = new ContainerAppJob(resourceGroupName, servicebus, containerRegistry.containerRegistry);

export const servicebusNamespaceName = servicebus.namespace.name;
export const servicebusQueueName = servicebus.queue.name;
export const containerAppJobName = containerAppJob.job.name;
