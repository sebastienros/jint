/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-8.js
 * @description Object.create - argument 'Properties' is a Boolean object whose primitive value is true (15.2.3.7 step 2).
 */


function testcase() {

        var props = new Boolean(true);
        var result = false;

        Object.defineProperty(props, "prop", {
            get: function () {
                result = this instanceof Boolean;
                return {};
            },
            enumerable: true
        });
        Object.create({}, props);
        return result;
    }
runTestCase(testcase);
