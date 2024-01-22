import { ContainerAppJob } from "./resources/containerAppJob";
import { resourceGroupName } from "./lib/config";
import { Servicebus } from "./resources/servicebus";
import * as azure_native from "@pulumi/azure-native";

const servicebus = new Servicebus(resourceGroupName);

const containerAppJob = new ContainerAppJob(resourceGroupName, servicebus);

export const servicebusNamespaceName = servicebus.namespace.name;
export const servicebusQueueName = servicebus.queue.name;
export const containerAppJobName = containerAppJob.job.name;
