load('ext://helm_remote', 'helm_remote')
load('ext://uibutton', 'cmd_button', 'location')

secret_settings(disable_scrub=True)

k8s_yaml('./kubernetes/ingress.yaml')
k8s_resource(new_name='ingress', labels=['ingress'], objects=['fbapp-ingress'])

include('./src/FbApp.Api/Tiltfile')
include('./src/FbApp.Api.Database/Tiltfile')
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

cmd_button(
    name='launch_fbapp',
    argv=['kubectl', 'port-forward', 'service/ingress-nginx-controller', '8090:443', '--namespace', 'ingress-nginx'],
    text='Launch FbApp',
    location=location.NAV,
    icon_name='rocket_launch'
)
