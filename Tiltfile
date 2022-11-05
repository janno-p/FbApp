load('ext://helm_remote', 'helm_remote')
load('ext://uibutton', 'cmd_button', 'location')

secret_settings(disable_scrub=True)

helm_remote('ingress-nginx',
    create_namespace=True,
    namespace='ingress-nginx',
    release_name='ingress-nginx',
    repo_name='ingress-nginx',
    repo_url='https://kubernetes.github.io/ingress-nginx',
    set=[
        'controller.config.use-forwarded-headers=true'
    ])

k8s_resource(new_name='ingress-nginx-resources', labels=['ingress'], objects=[
    'ingress-nginx-admission:clusterrole',
    'ingress-nginx-admission:clusterrolebinding',
    'ingress-nginx-admission:role',
    'ingress-nginx-admission:rolebinding',
    'ingress-nginx-admission:serviceaccount',
    'ingress-nginx-admission:validatingwebhookconfiguration',
    'ingress-nginx-controller:configmap',
    'ingress-nginx:clusterrole',
    'ingress-nginx:clusterrolebinding',
    'ingress-nginx:namespace',
    'ingress-nginx:role',
    'ingress-nginx:rolebinding',
    'ingress-nginx:serviceaccount',
    'nginx:ingressclass'
])

k8s_resource('ingress-nginx-admission-create', labels=['ingress'], resource_deps=['ingress-nginx-resources'])
k8s_resource('ingress-nginx-admission-patch', labels=['ingress'], resource_deps=['ingress-nginx-resources'])
k8s_resource('ingress-nginx-controller', labels=['ingress'], port_forwards='8090:443', resource_deps=['ingress-nginx-resources'])


# Probably fails on first run (when ingress controller is also configured and webhook is not ready in time)
# Can be fixed by reloading the resource on tilt dashboard

k8s_yaml('./kubernetes/ingress.yaml')
k8s_resource(new_name='ingress', labels=['ingress'], resource_deps=['ingress-nginx-controller'], objects=[
    'fbapp-ingress'
])

helm_remote('dapr',
    create_namespace=True,
    namespace='dapr-system',
    release_name='dapr',
    repo_name='dapr',
    repo_url='https://dapr.github.io/helm-charts/'
)

k8s_resource(new_name='dapr-resources', labels=['dapr'], objects=[
    'components.dapr.io:customresourcedefinition',
    'configurations.dapr.io:customresourcedefinition',
    'dapr-operator-admin:clusterrole',
    'dapr-operator:clusterrolebinding',
    'dapr-operator:serviceaccount',
    'dapr-role-tokenreview-binding:clusterrolebinding',
    'dapr-secret-reader:rolebinding',
    'dapr-sidecar-injector-cert:secret',
    'dapr-sidecar-injector:mutatingwebhookconfiguration',
    'dapr-system:namespace',
    'dapr-trust-bundle:secret',
    'dapr-webhook-cert:secret',
    'dapr-webhook-ca:secret',
    'daprsystem:configuration',
    'dashboard-reader-global:clusterrolebinding',
    'dashboard-reader:clusterrole',
    'dashboard-reader:serviceaccount',
    'resiliencies.dapr.io:customresourcedefinition',
    'secret-reader:role',
    'subscriptions.dapr.io:customresourcedefinition'
])

k8s_resource('dapr-dashboard', labels=['dapr'], port_forwards=[port_forward(8080, 8080, name='dapr dashboard')], resource_deps=['dapr-resources'])
k8s_resource('dapr-operator', labels=['dapr'], resource_deps=['dapr-resources'])
k8s_resource('dapr-sentry', labels=['dapr'], resource_deps=['dapr-resources'])
k8s_resource('dapr-placement-server', labels=['dapr'], resource_deps=['dapr-resources'])
k8s_resource('dapr-sidecar-injector', labels=['dapr'], resource_deps=['dapr-resources'])

include('./src/FbApp.Api/Tiltfile')
include('./src/FbApp.Api.EventStore/Tiltfile')
include('./src/FbApp.Auth/Tiltfile')
include('./src/FbApp.Auth.Database/Tiltfile')
include('./src/FbApp.Proxy/Tiltfile')
include('./src/FbApp.Web/Tiltfile')

cmd_button(
    name='open_fbapp',
    argv=['explorer', 'https://localhost:8090'],
    text='FbApp',
    location=location.NAV,
    icon_name='open_in_browser'
)

cmd_button(
    name='open_eventstore_dashboard',
    argv=['explorer', 'http://localhost:2113'],
    text='EventStore Dashboard',
    location=location.NAV,
    icon_name='compost'
)

cmd_button(
    name='open_dapr_dashboard',
    argv=['explorer', 'http://localhost:8080'],
    text='Dapr Dashboard',
    location=location.NAV,
    icon_name='hive'
)
