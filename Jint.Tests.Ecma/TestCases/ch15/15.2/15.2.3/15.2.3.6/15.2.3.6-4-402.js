/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-402.js
 * @description ES5 Attributes - [[Value]] attribute of inherited property of [[Prototype]] internal property is correct (String instance)
 */


function testcase() {
        try {
            Object.defineProperty(String.prototype, "prop", {
                value: 1001,
                writable: true,
                enumerable: true,
                configurable: true
            });
            var strObj = new String();

            return !strObj.hasOwnProperty("prop") && strObj.prop === 1001;
        } finally {
            delete String.prototype.prop;
        }
    }
runTestCase(testcase);
