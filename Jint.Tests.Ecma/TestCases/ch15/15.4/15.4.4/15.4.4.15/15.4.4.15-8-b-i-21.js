/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-21.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is inherited accessor property without a get function on an Array
 */


function testcase() {
        try {
            Object.defineProperty(Array.prototype, "0", {
                set: function () { },
                configurable: true
            });
            return [, ].lastIndexOf(undefined) === 0;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
