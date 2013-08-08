/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-313.js
 * @description Object.create - [[Configurable]] is set as false if it is absent in accessor descriptor of one property in 'Properties' (8.12.9 step 4.b)
 */


function testcase() {       
        var newObj = Object.create({}, {
            prop: {
                set: function () { },
                get: function () { },
                enumerable: true
            }
        });
        var hasProperty = newObj.hasOwnProperty("prop");
        delete newObj.prop;
        var isNotConfigurable = newObj.hasOwnProperty("prop");
        return hasProperty && isNotConfigurable;
    }
runTestCase(testcase);
