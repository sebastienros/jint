/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-18.js
 * @description Array.prototype.indexOf - decreasing length of array with prototype property causes prototype index property to be visited
 */


function testcase() {

        var arr = [0, 1, 2];

        try {
            Object.defineProperty(Array.prototype, "2", {
                get: function () {
                    return "prototype";
                },
                configurable: true
            });

            Object.defineProperty(arr, "1", {
                get: function () {
                    arr.length = 2;
                    return 1;
                },
                configurable: true
            });

            return 2 === arr.indexOf("prototype");
        } finally {
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
