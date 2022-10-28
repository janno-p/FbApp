import { defineConfig } from 'windicss/helpers'
import colors from 'windicss/colors'
import typography from 'windicss/plugin/typography'

export default defineConfig({
    attributify: true,
    darkMode: 'class',
    extract: {
        include: ['src/**/*.elm']
    },
    plugins: [
        typography()
    ],
    theme: {
        extend: {
            colors: {
                primary: '#1976D2',
                secondary: '#26A69A',
                accent: '#9C27B0',

                dark: '#1D1D1D',

                positive: '#21BA45',
                negative: '#C10015',
                info: '#31CCEC',
                warning: '#F2C037'
            },
            typography: {
                DEFAULT: {
                    css: {
                        a: {
                            '&:hover': {
                                opacity: 1,
                                color: colors.teal[600]
                            },
                            color: 'inherit',
                            fontWeight: '500',
                            opacity: 0.75,
                            textDecoration: 'underline'
                        },
                        b: {
                            color: 'inherit'
                        },
                        code: {
                            color: 'inherit'
                        },
                        color: 'inherit',
                        em: {
                            color: 'inherit'
                        },
                        h1: {
                            color: 'inherit'
                        },
                        h2: {
                            color: 'inherit'
                        },
                        h3: {
                            color: 'inherit'
                        },
                        h4: {
                            color: 'inherit'
                        },
                        maxWidth: '65ch',
                        strong: {
                            color: 'inherit'
                        }
                    }
                }
            }
        }
    }
})
