import * as pulumi from "@pulumi/pulumi";

export const config = new pulumi.Config();

export const resourceGroupName = config.require("resourceGroup");
