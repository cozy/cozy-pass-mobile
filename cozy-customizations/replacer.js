const path = require('path')
const fs = require('fs')
const replacements = require('./replacements.json')

const assert = (cond, msg) => {
    if (!cond) {
        throw new Error(msg)
    }
}

const main = async () => {
    for (let replacement of replacements) {
        const src = path.join(__dirname, replacement.src) 
        assert(fs.existsSync(src), 'Path does not exist:' + src)
        for (let rawDest of replacement.dests) {
            const dest = path.join(__dirname, rawDest) 
            assert(fs.existsSync(dest), 'Path does not exist:' + dest)
            console.log('Replacing ', dest, 'by', src)
            fs.copyFileSync(src, dest)
        }
    }
}

main().catch(e => {
    console.error(e)
    process.exit(1)
}) 
