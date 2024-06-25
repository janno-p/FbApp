FROM bitnami/os-shell
USER 0
COPY ./store.3.zip /opt/files/backup.zip
RUN mkdir /var/lib/apt/lists && apt-get update && apt-get install unzip -y && \
    apt-get clean && rm -rf /var/lib/apt/lists /var/cache/apt/archives
USER 1001
