/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        './**/*.fs'
    ],
    plugins: [
        require('@tailwindcss/forms'),
        require('@tailwindcss/aspect-ratio'),
        require('@tailwindcss/typography'),
    ]
}
