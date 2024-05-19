const { addDynamicIconSelectors } = require('@iconify/tailwind')
const daisyui = require('daisyui')
const typography = require('@tailwindcss/typography')

/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        './**/*.fs'
    ],

    plugins: [
        '@tailwindcss/forms',
        '@tailwindcss/aspect-ratio',
        typography,
        daisyui,
        addDynamicIconSelectors()
    ],

    theme: {
        extend: {
            colors: {
                "primary": "#1976D2",
                "secondary": "#26A69A",
                "tertiary": "#555555",

                "accent": "#9C27B0",
                "dark": "#1D1D1D",

                "neutral": "#E0E1E2",
                "positive": "#21BA45",
                "negative": "#C10015",
                "info": "#31CCEC",
                "warning": "#F2C037"
            }
        }
    }
}
