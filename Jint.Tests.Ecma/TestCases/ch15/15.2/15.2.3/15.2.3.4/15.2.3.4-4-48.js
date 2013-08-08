/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-48.js
 * @description Object.getOwnPropertyNames - own accessor property of Array object 'O' is pushed into the returned array.
 */


function testcase() {
        var arr = [0, 1, 2];

        Object.defineProperty(arr, "ownProperty", {
            get: function () {
                return "ownArray";
            },
            configurable: true
        });

        var result = Object.getOwnPropertyNames(arr);

        for (var p in result) {
            if (result[p] === "ownProperty") {
                return true;
            }
        }

        return false;
    }
runTestCase(testcase);
