declare module '*.elm' {
    interface IElmMain {
        ports: {
            onStoreChange: {
                send(args: any): void
            },
            signOut: {
                subscribe(callback: (args: any) => Promise<void>): void
            }
        }
    }

    export const Elm: {
        Main: {
            init(args: { node: Element }): IElmMain
        }
    }
}
