/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-41.js
 * @description Object.defineProperties - type of desc.value is different from type of P.value (8.12.9 step 6)
 */


function testcase() {

        var obj = {};

        obj.foo = 101; // default value of attributes: writable: true, configurable: true, enumerable: true

        Object.defineProperties(obj, {
            foo: {
                value: "102"
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", "102", true, true, true);

    }
runTestCase(testcase);
