import * as operationalinsights from "@pulumi/azure-native/operationalinsights";
import { resourceName } from "../lib/utils";
import * as pulumi from "@pulumi/pulumi";

export class LogAnalyticsWorkspace {
  public workspace: operationalinsights.Workspace;
  // public primarySharedKey: string | undefined;

  constructor(resourceGroupName: string) {
    const workspaceName = resourceName("workspace-log");

    this.workspace = new operationalinsights.Workspace("main", {
      resourceGroupName,
      workspaceName: workspaceName,
      sku: {
        name: operationalinsights.WorkspaceSkuNameEnum.PerGB2018
      },
    });
  };

  public getPrimarySharedKey(resourceGroupName: string): pulumi.Output<string> {
    const sharedKeys = pulumi
      .all([this.workspace.name, resourceGroupName])
      .apply(([name, resourceGroupName]) =>
        operationalinsights.getSharedKeys({
          resourceGroupName: resourceGroupName,
          workspaceName: name,
      })
    );

    return sharedKeys.apply(keys => keys.primarySharedKey!);
  }
}
