/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-8.js
 * @description Array.prototype.lastIndexOf -  properties added into own object after current position are visited on an Array
 */


function testcase() {

        var arr = [0, , 2];

        Object.defineProperty(arr, "2", {
            get: function () {
                Object.defineProperty(arr, "1", {
                    get: function () {
                        return 1;
                    },
                    configurable: true
                });
                return 0;
            },
            configurable: true
        });

        return arr.lastIndexOf(1) === 1;
    }
runTestCase(testcase);
