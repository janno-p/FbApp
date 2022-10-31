load('ext://helm_remote', 'helm_remote')

secret_settings(disable_scrub=True)

include('./src/FbApp.Api/Tiltfile')
include('./src/FbApp.Api.EventStore/Tiltfile')
include('./src/FbApp.Auth/Tiltfile')
include('./src/FbApp.Auth.Database/Tiltfile')
include('./src/FbApp.Proxy/Tiltfile')
include('./src/FbApp.Web/Tiltfile')

helm_remote(
    'ingress-nginx',
    release_name='ingress-nginx',
    repo_name='ingress-nginx',
    repo_url='https://kubernetes.github.io/ingress-nginx',
    set=[
        'controller.config.use-forwarded-headers=true'
    ]
)

k8s_resource('ingress-nginx-admission-create', labels=['ingress-nginx'])
k8s_resource('ingress-nginx-admission-patch', labels=['ingress-nginx'])
k8s_resource('ingress-nginx-controller', port_forwards='8090:443', labels=['ingress-nginx'])

helm_remote('dapr', release_name='dapr', repo_name='dapr', repo_url='https://dapr.github.io/helm-charts/')
k8s_resource('dapr-dashboard', port_forwards=[port_forward(8080, 8080, name='dapr dashboard')], labels=['dapr'])
k8s_resource('dapr-operator', labels=['dapr'])
k8s_resource('dapr-sentry', labels=['dapr'])
k8s_resource('dapr-placement-server', labels=['dapr'])
k8s_resource('dapr-sidecar-injector', labels=['dapr'])

k8s_yaml('./kubernetes/ingress.yaml')
