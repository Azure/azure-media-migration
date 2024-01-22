import * as pulumi from "@pulumi/pulumi";

const stack = pulumi.getStack();
const projectName = pulumi.getProject();

/**
 * Create resource name in the format: `<stack>-<project_name>-<suffix>`
 */
export const resourceName = (suffix: string) => `${stack}-${projectName}-${suffix}`;

/**
 * Create storage account name in the format `<stack><project_name><suffix>`
 * 
 * Removes all non-letter characters.
 */
export const storageAccountName = (suffix: string) => resourceName(suffix).replace(/[^a-zA-Z]/, '');
