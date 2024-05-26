import { copyFile } from 'fs'
import { dirname, resolve } from 'path'
import { fileURLToPath } from 'url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)

const sourceFile = resolve(__dirname, './node_modules/htmx.org/dist/htmx.min.js')
const targetFile = resolve(__dirname, './FbApp/wwwroot/js/htmx.min.js')

copyFile(sourceFile, targetFile, (err) => {
    if (err) {
        throw err
    }
    console.log('htmx was copied to wwwroot')
})
