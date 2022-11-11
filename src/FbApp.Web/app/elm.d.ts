declare module '*.elm' {
    interface IElmMain {
        ports: {
            randomBytes: {
                send(bytes: number[]): void
            },
            generateRandomBytes: {
                subscribe(callback: (numberOfBytes: number) => void): void
            }
        }
    }

    export const Elm: {
        Main: {
            init(args: { node: Element, flags: number[] | undefined }): IElmMain
        }
    }
}
