import * as pulumi from "@pulumi/pulumi";
import { containerregistry } from "@pulumi/azure-native";
import * as Constants from "../lib/constants";

export class ContainerRegistry {
  public containerRegistry: pulumi.Output<containerregistry.GetRegistryResult>;

  constructor(resourceGroupName: string) {
    this.containerRegistry = containerregistry.getRegistryOutput({
      resourceGroupName,
      registryName: Constants.ContainerRegistryName
    });
  };
}
