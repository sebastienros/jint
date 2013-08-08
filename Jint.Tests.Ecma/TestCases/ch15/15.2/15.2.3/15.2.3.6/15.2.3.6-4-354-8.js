/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-354-8.js
 * @description ES5 Attributes - property 'P' with attributes [[Writable]]: false, [[Enumerable]]: true, [[Configurable]]: true is non-writable using simple assignment, 'O' is the global object
 */


function testcase() {
        var obj = fnGlobalObject();
        try {
            Object.defineProperty(obj, "prop", {
                value: 2010,
                writable: false,
                enumerable: true,
                configurable: true
            });
            var valueVerify = (obj.prop === 2010);
            obj.prop = 1001;

            return valueVerify && obj.prop === 2010;
        } finally {
            delete obj.prop;
        }
    }
runTestCase(testcase);
