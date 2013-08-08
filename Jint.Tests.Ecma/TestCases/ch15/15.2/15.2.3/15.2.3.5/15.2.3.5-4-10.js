/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-10.js
 * @description Object.create - argument 'Properties' is the Math object (15.2.3.7 step 2)
 */


function testcase() {

        var result = false;
        Object.defineProperty(Math, "prop", {
            get: function () {
                result = (this === Math);
                return {};
            },
            enumerable: true,
            configurable: true
        });

        try {
            var newObj = Object.create({}, Math);
            return result && newObj.hasOwnProperty("prop");
        } finally {
            delete Math.prop;
        }
    }
runTestCase(testcase);
