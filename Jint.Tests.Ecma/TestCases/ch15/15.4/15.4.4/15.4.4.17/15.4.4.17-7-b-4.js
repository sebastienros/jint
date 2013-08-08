/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-4.js
 * @description Array.prototype.some - properties added into own object after current position are visited on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 1 && val === 1) {
                return true;
            } else {
                return false;
            }
        }
        
        var arr = { length: 2 };

        Object.defineProperty(arr, "0", {
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

        return Array.prototype.some.call(arr, callbackfn);
    }
runTestCase(testcase);
