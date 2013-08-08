/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-6.js
 * @description Object.create - argument 'Properties' is an Array object (15.2.3.7 step 2).
 */


function testcase() {

        var props = [];
        var result = false;

        Object.defineProperty(props, "prop", {
            get: function () {
                result = this instanceof Array;
                return {};
            },
            enumerable: true
        });
        Object.create({}, props);
        return result;
    }
runTestCase(testcase);
