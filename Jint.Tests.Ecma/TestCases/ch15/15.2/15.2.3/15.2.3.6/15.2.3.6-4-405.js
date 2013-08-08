/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-405.js
 * @description ES5 Attributes - Failed to add a property to an object when the object's object has a property with same name and [[Writable]] attribute is set to false (Number instance)
 */


function testcase() {
        try {
            Object.defineProperty(Number.prototype, "prop", {
                value: 1001,
                writable: false,
                enumerable: false,
                configurable: true
            });
            var numObj = new Number();
            numObj.prop = 1002;

            return !numObj.hasOwnProperty("prop") && numObj.prop === 1001;
        } finally {
            delete Number.prototype.prop;
        }
    }
runTestCase(testcase);
