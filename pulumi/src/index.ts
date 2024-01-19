import { servicebusNamespace, servicebusQueue } from "./resources/servicebus";
import { job as containerAppJob } from "./resources/containerAppJob";

export const servicebusName = servicebusNamespace.name;
export const servicebusQueueName = servicebusQueue.name;
export const containerAppJobName = containerAppJob.name;
