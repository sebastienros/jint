/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-39.js
 * @description Object.defineProperties - 'P' is data descriptor and every fields in 'desc' is the same with 'P' (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        obj.foo = 101; // default value of attributes: writable: true, configurable: true, enumerable: true

        Object.defineProperties(obj, {
            foo: {
                value: 101,
                enumerable: true,
                writable: true,
                configurable: true
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 101, true, true, true);

    }
runTestCase(testcase);
