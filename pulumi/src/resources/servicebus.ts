import * as servicebus from "@pulumi/azure-native/servicebus";
import { resourceName } from "../lib/utils";

export class Servicebus {
  public namespace: servicebus.Namespace;
  public queue: servicebus.Queue;
  public resourceGroupName: string;

  constructor(resourceGroupName: string) {
    this.resourceGroupName = resourceGroupName;

    this.namespace = new servicebus.Namespace("main", {
      resourceGroupName,
      namespaceName: resourceName("sbns"),
      sku: {
        name: servicebus.SkuName.Basic,
        tier: servicebus.SkuTier.Basic
      },
    });

    this.queue = new servicebus.Queue("main", {
      resourceGroupName,
      namespaceName: this.namespace.name,
      queueName: resourceName("sbq"),
    });
  }

  public getPrimaryConnectionString = () => (
    servicebus
      .listNamespaceKeysOutput({
        resourceGroupName: this.resourceGroupName,
        namespaceName: this.namespace.name,
        authorizationRuleName: 'RootManageSharedAccessKey'
      })
      .primaryConnectionString
  );
}
