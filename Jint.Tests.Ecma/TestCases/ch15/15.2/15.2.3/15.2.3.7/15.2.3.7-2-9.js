/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-9.js
 * @description Object.defineProperties - argument 'Properties' is a Function object
 */


function testcase() {

        var obj = {};
        var props = function () { };
        var result = false;
      
        Object.defineProperty(props, "prop", {
            get: function () {
                result = this instanceof Function;
                return {};
            },
            enumerable: true
        });

        Object.defineProperties(obj, props);
        return result;
    }
runTestCase(testcase);
