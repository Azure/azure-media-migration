import * as operationalinsights from "@pulumi/azure-native/operationalinsights/v20230901";
import { resourceName } from "../lib/utils";

export class LogAnalyticsWorkspace {
  public workspace: operationalinsights.Workspace;

  constructor(resourceGroupName: string) {
    this.workspace = new operationalinsights.Workspace("main", {
      resourceGroupName,
      workspaceName: resourceName("workspace-log"),
      sku: {
        name: operationalinsights.WorkspaceSkuNameEnum.PerGB2018
      },
    });
  };
}
