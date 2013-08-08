/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-18.js
 * @description Object.defineProperties - argument 'Properties' is the global object
 */


function testcase() {

        var obj = {};
        var result = false;

        try {
            Object.defineProperty(fnGlobalObject(), "prop", {
                get: function () {
                    result = (this === fnGlobalObject());
                    return {};
                },
                enumerable: true,
				configurable:true
            });

            Object.defineProperties(obj, fnGlobalObject());
            return result;
        } catch (e) {
            return (e instanceof TypeError);
        } finally {
            delete fnGlobalObject().prop;
        }
    }
runTestCase(testcase);
