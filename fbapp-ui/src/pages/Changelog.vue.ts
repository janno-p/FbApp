import { defineComponent } from 'vue'

const log = [
    {
        version: '0.1.0',
        changes: [
            'Uuendatud rakenduse aluseks olevaid raamistikke',
            'Lisatud Dapr runtime',
            'Rohkem teenuseid!!!',
            'Animatsioonid',
            'Autentimine ja autoriseerimine JWT tokenitega',
            'Rakendus arvestab ka olukorraga, kus aktiivne võistlus on veel määramata'
        ]
    },
    {
        version: '0.0.4',
        changes: [
            'Mängutulemuste kontrollimine v2 API-ga',
            'Väljalangemismängude tulemuste näitamine',
            'Üldise ennustuste punktitabeli näitamine',
            'Mängude järjekorra parandamine'
        ]
    },
    {
        version: '0.0.3',
        changes: [
            'Parandatud iluviga Chromes',
            'Võistluste alguskuupäeva registreerimine',
            'Mängutulemuste kontrollimine',
            'Võistluste alguskuupäeva kontrollimine ennustuste registreerimisel'
        ]
    },
    {
        version: '0.0.2',
        changes: [
            'Oma ennustuste vaade',
            'Peale ennustuste registreerimist oma ennustuste vaate avamine',
            'Olemasoleva ennustuste korral oma ennustuste vaate avamine',
            'Juhusliku valiku tegemise võimalus'
        ]
    },
    {
        version: '0.0.1',
        changes: [
            'Ennustuste registreerimine'
        ]
    }
]

export default defineComponent({
    name: 'Changelog',

    setup () {
        return { log }
    }
})
