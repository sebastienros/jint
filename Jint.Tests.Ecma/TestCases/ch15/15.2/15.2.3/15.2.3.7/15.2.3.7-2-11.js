/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-11.js
 * @description Object.defineProperties - argument 'Properties' is the Math object
 */


function testcase() {

        var obj = {};
        var result = false;
       
        try {
            Object.defineProperty(Math, "prop", {
                get: function () {
                    result = (this === Math);
                    return {};
                },
                enumerable: true,
                configurable: true
            });

            Object.defineProperties(obj, Math);
            return result;
        } finally {
            delete Math.prop;
        }
    }
runTestCase(testcase);
