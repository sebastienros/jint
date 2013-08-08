/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-22.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is inherited accessor property without a get function on an Array-like object
 */


function testcase() {

        try {
            Object.defineProperty(Object.prototype, "0", {
                set: function () { },
                configurable: true
            });
            return 0 === Array.prototype.lastIndexOf.call({ length: 1 }, undefined);
        } finally {
            delete Object.prototype[0];
        }
    }
runTestCase(testcase);
