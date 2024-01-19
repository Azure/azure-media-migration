import * as pulumi from "@pulumi/pulumi";

const config = new pulumi.Config();

export const resourceGroupName = config.require("resourceGroup");

export const getEnv = (name: string): string => {
  const value = process.env[name];
  if (!value) throw new ReferenceError(`Environment variable "${name}" is not defined.`);

  return value;
}
