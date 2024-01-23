FROM ubuntu:23.10

RUN apt update -y && \
    apt install -y \
        ca-certificates \
        dotnet-sdk-6.0 \
        ffmpeg \
        git-lfs \
        openssl \
        npm && \
    npm install -g \
        azurite

RUN mkdir /cert && \
    openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
        -keyout /cert/127.0.0.1.key \
        -out /cert/127.0.0.1.crt \
        -subj "/CN=127.0.0.1" \
        -addext "subjectAltName=IP:127.0.0.1" && \
    cp /cert/127.0.0.1.crt /usr/local/share/ca-certificates/ && \
    update-ca-certificates
