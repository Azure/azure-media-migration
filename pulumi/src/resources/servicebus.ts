import * as servicebus from "@pulumi/azure-native/servicebus";
import { resourceGroupName } from "../lib/config";

export const servicebusNamespace = new servicebus.Namespace("main", {
  resourceGroupName,
  namespaceName: "ams-migration-sbns",
  sku: {
    name: servicebus.SkuName.Basic,
    tier: servicebus.SkuTier.Basic
  },
});

export const servicebusQueue = new servicebus.Queue("main", {
  resourceGroupName,
  namespaceName: servicebusNamespace.name,
  queueName: "ams-migration-sbq"
});

export const servicebusQueueOutput = servicebus.getNamespaceOutput({
  resourceGroupName,
  namespaceName: servicebusNamespace.name,
});
