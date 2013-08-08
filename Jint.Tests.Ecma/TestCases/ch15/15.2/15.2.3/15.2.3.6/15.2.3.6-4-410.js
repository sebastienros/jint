/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-410.js
 * @description ES5 Attributes - Failed to add a property to an object when the object's prototype has a property with the same name and [[Writable]] set to false (JSON)
 */


function testcase() {
        try {
            Object.defineProperty(Object.prototype, "prop", {
                value: 1001,
                writable: false,
                enumerable: false,
                configurable: true
            });
            JSON.prop = 1002;

            return !JSON.hasOwnProperty("prop") && JSON.prop === 1001;
        } finally {
            delete Object.prototype.prop;
        }
    }
runTestCase(testcase);
