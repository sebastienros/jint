/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-46.js
 * @description Object.getOwnPropertyNames - inherited accessor property of Array object 'O' is not pushed into the returned array.
 */


function testcase() {
        try {
            var arr = [0, 1, 2];

            Object.defineProperty(Array.prototype, "protoProperty", {
                get: function () {
                    return "protoArray";
                },
                configurable: true
            });

            var result = Object.getOwnPropertyNames(arr);

            for (var p in result) {
                if (result[p] === "protoProperty") {
                    return false;
                }
            }
            return true;
        } finally {
            delete Array.prototype.protoProperty;
        }
    }
runTestCase(testcase);
