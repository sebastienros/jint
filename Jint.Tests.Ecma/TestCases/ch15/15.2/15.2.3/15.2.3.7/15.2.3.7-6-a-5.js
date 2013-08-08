/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-5.js
 * @description Object.defineProperties - 'P' is own accessor property (8.12.9 step 1 ) 
 */


function testcase() {
        var obj = {};
        function getFunc() {
            return 11;
        }

        Object.defineProperty(obj, "prop", {
            get: getFunc,
            configurable: false
        });

        try {
            Object.defineProperties(obj, {
                prop: {
                    value: 12,
                    configurable: true
                }
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && accessorPropertyAttributesAreCorrect(obj, "prop", getFunc, undefined, undefined, false, false);
        }
    }
runTestCase(testcase);
