const getEnv = (name: string): string => {
  const value = process.env[name];
  if (!value) throw new ReferenceError(`Environment variable "${name}" is not defined.`);

  return value;
}

export const ContainerRegistry = getEnv("CONTAINER_REGISTRY");

export const ContainerAppJobImageName = getEnv("CONTAINER_APP_JOB_IMAGE_NAME");

export const ContainerAppJobImageTag = getEnv("CONTAINER_APP_JOB_IMAGE_TAG");
