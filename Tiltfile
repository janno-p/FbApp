load('ext://helm_remote', 'helm_remote')
load('ext://restart_process', 'docker_build_with_restart')
load('ext://uibutton', 'cmd_button', 'location')

secret_settings(disable_scrub=True)


# ==============================================================================
# UI buttons
# ==============================================================================

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


# ==============================================================================
# Install Helm chart without deployments
# ==============================================================================

chart_values = []
if os.path.exists('./values.user.yaml'):
    chart_values.append('./values.user.yaml')

chart_yaml = helm(
    './chart',
    name='fbapp',
    namespace='default',
    values=chart_values
)

deployments, chart_yaml = filter_yaml(chart_yaml, kind='deployment')
ingress, chart_yaml = filter_yaml(chart_yaml, kind='ingress')

k8s_yaml(chart_yaml)


# ==============================================================================
# Utilities
# ==============================================================================

def replace_deployment_image(deployment_name, image_name):
    deployment, yaml = filter_yaml(deployments, name=deployment_name)
    deployment_yaml = decode_yaml(deployment)
    deployment_yaml['spec']['template']['spec']['containers'][0]['image'] = image_name
    return encode_yaml(deployment_yaml)

def use_vite_devserver_port():
    ingress_yaml = decode_yaml(ingress)
    ingress_yaml['spec']['rules'][0]['http']['paths'][3]['backend']['service']['port']['number'] = 5173
    return encode_yaml(ingress_yaml)


# ==============================================================================
# Ingress
# ==============================================================================

k8s_resource(new_name='ingress', labels=['fbapp'], objects=[
    'fbapp:ingress',
    'fbapp:serviceaccount'
])


# ==============================================================================
# MongoDB
# ==============================================================================

k8s_resource('fbapp-api-database-mongodb', labels=['fbapp-api'], port_forwards='27017:27017', resource_deps=[
    'fbapp-api-resources'
])


# ==============================================================================
# EventStoreDB
# ==============================================================================

k8s_resource('fbapp-eventstore', port_forwards='2113:2113', labels=['fbapp-api'], resource_deps=[
    'fbapp-api-resources'
])


# ==============================================================================
# Valkey
# ==============================================================================

k8s_resource('fbapp-api-valkey-master', labels=['fbapp-api'], resource_deps=[
    'fbapp-api-resources'
])


# ==============================================================================
# PostgreSQL
# ==============================================================================

k8s_resource('fbapp-auth-database-postgresql', labels=['fbapp-auth'], port_forwards='5432:5432', resource_deps=[
    'fbapp-auth-resources'
])


# ==============================================================================
# API service
# ==============================================================================

local_resource(
    'fbapp-api-build',
    'dotnet publish --configuration Debug --output out',
    deps=['fbapp-api'],
    dir='./src/FbApp.Api',
    ignore=['obj'],
    labels=['fbapp-api']
)

docker_build_with_restart(
    'fbapp-api-image',
    './src/FbApp.Api/out',
    entrypoint=['dotnet', 'FbApp.Api.dll'],
    dockerfile='./src/FbApp.Api/Dockerfile.Development',
    live_update=[
        sync('./src/FbApp.Api/out', '/app/out')
    ]
)

k8s_yaml(replace_deployment_image('fbapp-api', 'fbapp-api-image'))

k8s_resource(new_name='fbapp-api-resources', labels=['fbapp-api'], objects=[
    'fbapp-api-database-mongodb:networkpolicy',
    'fbapp-api-database-mongodb:poddisruptionbudget',
    'fbapp-api-database-mongodb:secret',
    'fbapp-api-database-mongodb:serviceaccount',
    'fbapp-api-database-mongodb-common-scripts:configmap',
    'fbapp-api-valkey:networkpolicy',
    'fbapp-api-valkey-configuration:configmap',
    'fbapp-api-valkey-health:configmap',
    'fbapp-api-valkey-master:serviceaccount',
    'fbapp-api-valkey-scripts:configmap',
    'fbapp-api-dapr-state:component',
    'fbapp-eventstore:persistentvolumeclaim'
])

k8s_resource('fbapp-api', labels=['fbapp-api'], resource_deps=[
    'fbapp-api-build',
    'fbapp-api-database-mongodb',
    'fbapp-eventstore'
])


# ==============================================================================
# Auth service
# ==============================================================================

local_resource(
    'fbapp-auth-build',
    'dotnet publish --configuration Release --output out',
    deps=['fbapp-auth'],
    dir='./src/FbApp.Auth',
    ignore=['obj'],
    labels=['fbapp-auth']
)

docker_build_with_restart(
    'fbapp-auth-image',
    './src/FbApp.Auth/out',
    entrypoint=['dotnet', 'FbApp.Auth.dll'],
    dockerfile='./src/FbApp.Auth/Dockerfile.Development',
    live_update=[
        sync('./src/FbApp.Auth/out', '/app/out')
    ]
)

k8s_yaml(replace_deployment_image('fbapp-auth', 'fbapp-auth-image'))

k8s_resource(new_name='fbapp-auth-resources', labels=['fbapp-auth'], objects=[
    'fbapp-auth-database-postgresql:networkpolicy',
    'fbapp-auth-database-postgresql:poddisruptionbudget',
    'fbapp-auth-database-postgresql:secret',
    'fbapp-auth-database-postgresql:serviceaccount'
])

k8s_resource('fbapp-auth', labels=['fbapp-auth'], resource_deps=[
    'fbapp-auth-build',
    'fbapp-auth-database-postgresql'
])


# ==============================================================================
# Proxy service
# ==============================================================================

local_resource(
    'fbapp-proxy-build',
    'dotnet publish --configuration Release --output out',
    deps=['fbapp-proxy'],
    dir='./src/FbApp.Proxy',
    ignore=['obj'],
    labels=['fbapp-proxy']
)

docker_build_with_restart(
    'fbapp-proxy-image',
    './src/FbApp.Proxy/out',
    entrypoint=['dotnet', 'FbApp.Proxy.dll'],
    dockerfile='./src/FbApp.Proxy/Dockerfile.Development',
    live_update=[
        sync('./src/FbApp.Proxy/out', '/app/out')
    ]
)

k8s_yaml(replace_deployment_image('fbapp-proxy', 'fbapp-proxy-image'))

k8s_resource('fbapp-proxy', labels=['fbapp-proxy'], resource_deps=[
    'fbapp-proxy-build'
])


# ==============================================================================
# SPA service
# ==============================================================================

docker_build(
    'fbapp-web-image',
    './src/FbApp.Web',
    dockerfile='./src/FbApp.Web/Dockerfile.Development',
    live_update=[
        fall_back_on([
            './src/FbApp.Web/package.json',
            './src/FbApp.Web/yarn.lock',
            './src/FbApp.Web/.yarn/**/*',
            './src/FbApp.Web/.yarnrc.yml'
        ]),
        sync('./src/FbApp.Web', '/app')
    ]
)

k8s_yaml(replace_deployment_image('fbapp-web', 'fbapp-web-image'))

k8s_resource('fbapp-web', labels=['fbapp-web'])

k8s_yaml(use_vite_devserver_port())
