/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-14.js
 * @description Object.defineProperties - argument 'Properties' is the JSON object
 */


function testcase() {

        var obj = {};
        var result = false;

        try {
            Object.defineProperty(JSON, "prop", {
                get: function () {
                    result = (this === JSON);
                    return {};
                },
                enumerable: true,
                configurable: true
            });

            Object.defineProperties(obj, JSON);
            return result;
        } finally {
            delete JSON.prop;
        }
    }
runTestCase(testcase);
