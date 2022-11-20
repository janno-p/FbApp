declare module '*.elm' {
    interface IElmMain {
        ports: {
            randomBytes: {
                send(bytes: [number[], string]): void
            },
            generateRandomBytes: {
                subscribe(callback: ([numberOfBytes, path]: [number, string]) => void): void
            }
        }
    }

    export const Elm: {
        Main: {
            init(args: { node: Element, flags: [number[] | null, string] }): IElmMain
        }
    }
}
