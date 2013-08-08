/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-10.js
 * @description Object.defineProperties - argument 'Properties' is an Array object
 */


function testcase() {

        var obj = {};
        var props = [];
        var result = false;
        
        Object.defineProperty(props, "prop", {
            get: function () {
                result = this instanceof Array;
                return {};
            },
            enumerable: true
        });

        Object.defineProperties(obj, props);
        return result;
    }
runTestCase(testcase);
