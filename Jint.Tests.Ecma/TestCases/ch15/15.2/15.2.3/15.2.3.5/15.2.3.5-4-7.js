/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-7.js
 * @description Object.create - argument 'Properties' is a String object (15.2.3.7 step 2)
 */


function testcase() {

        var props = new String();
        var result = false;

        Object.defineProperty(props, "prop", {
            get: function () {
                result = this instanceof String;
                return {};
            },
            enumerable: true
        });
        Object.create({}, props);
        return result;
    }
runTestCase(testcase);
