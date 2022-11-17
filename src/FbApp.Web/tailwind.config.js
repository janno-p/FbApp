/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        'src/**/*.elm'
    ],
    theme: {
        extend: {
            colors: {
                primary: '#1976D2', // #027be3
                secondary: '#26A69A',
                tertiary: '#555555',

                accent: '#9C27B0',
                dark: '#1D1D1D',

                neutral: '#E0E1E2',
                positive: '#21BA45',
                negative: '#C10015', // #DB2828
                info: '#31CCEC',
                warning: '#F2C037'
            }
        },
    },
    plugins: [],
}
