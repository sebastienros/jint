/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-70.js
 * @description Object.defineProperties - 'P' is accessor property and  P.configurable is true, 'desc' in 'Properties' is data property (8.12.9 step 9.c.i)
 */


function testcase() {

        var obj = {};

        function get_func() {
            return 10;
        }

        Object.defineProperty(obj, "foo", {
            get: get_func,
            configurable: true
        });

        Object.defineProperties(obj, {
            foo: {
                value: 12
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 12, false, false, true);
    }
runTestCase(testcase);
