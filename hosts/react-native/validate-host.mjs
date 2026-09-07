import {readFileSync} from 'node:fs'

const host = JSON.parse(readFileSync(new URL('./host.json', import.meta.url), 'utf8'))
if (host.framework !== 'react-native') throw new Error('React mobile host must use React Native')
if (!['ios', 'android'].every(target => host.targets.includes(target))) throw new Error('React Native host must target iOS and Android')
if (!host.productInterfaceRevision || !host.capabilityManifestRevision) throw new Error('Host revisions are required')
console.log(JSON.stringify({status: 'PASS', host}, null, 2))
