/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-9.js
 * @description Array.prototype.indexOf - properties can be added to prototype after current position are visited on an Array-like object
 */


function testcase() {

        var arr = { length: 2 };

        Object.defineProperty(arr, "0", {
            get: function () {
                Object.defineProperty(Object.prototype, "1", {
                    get: function () {
                        return 6.99;
                    },
                    configurable: true
                });
                return 0;
            },
            configurable: true
        });

        try {
            return Array.prototype.indexOf.call(arr, 6.99) === 1;
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
