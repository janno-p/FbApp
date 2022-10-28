load('ext://helm_remote', 'helm_remote')

secret_settings(disable_scrub=True)

helm_remote('dapr', release_name='dapr', repo_name='dapr', repo_url='https://dapr.github.io/helm-charts/')
k8s_resource('dapr-dashboard', port_forwards=[port_forward(8080, 8080, name='dapr dashboard')], labels=['dapr'])
k8s_resource('dapr-operator', labels=['dapr'])
k8s_resource('dapr-sentry', labels=['dapr'])
k8s_resource('dapr-placement-server', labels=['dapr'])
k8s_resource('dapr-sidecar-injector', labels=['dapr'])
