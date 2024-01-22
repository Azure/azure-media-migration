const getEnv = (name: string): string => {
  const value = process.env[name];
  if (!value) throw new ReferenceError(`Environment variable "${name}" is not defined.`);

  return value;
}

export const SubscriptionId = getEnv("SUBSCRIPTION_ID");

export const ContainerRegistryName = getEnv("CONTAINER_REGISTRY");

export const ContainerRegistryServer = `${ContainerRegistryName}.azurecr.io`;

export const ContainerAppJobImageName = getEnv("CONTAINER_APP_JOB_IMAGE_NAME");

export const ContainerAppJobImageTag = getEnv("CONTAINER_APP_JOB_IMAGE_TAG");
