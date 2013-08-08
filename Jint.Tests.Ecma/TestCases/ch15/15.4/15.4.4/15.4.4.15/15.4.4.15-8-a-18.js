/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-18.js
 * @description Array.prototype.lastIndexOf -  decreasing length of array with prototype property causes prototype index property to be visited
 */


function testcase() {

        var arr = [0, 1, 2, 3, 4];

        try {
            Object.defineProperty(Array.prototype, "2", {
                get: function () {
                    return "prototype";
                },
                configurable: true
            });

            Object.defineProperty(arr, "3", {
                get: function () {
                    arr.length = 2;
                    return 1;
                },
                configurable: true
            });

            return 2 === arr.lastIndexOf("prototype");
        } finally {
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
