/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-102gs.js
 * @description Strict Mode - checking 'this' (strict anonymous function passed as arg to String.prototype.replace from non-strict context)
 * @onlyStrict
 */
var x = 3;
if ( ("ab".replace("b", (function () { 
                                "use strict";
                                return function () {
                                    x = this;
                                    return "a";
                                }
                           })())!=="aa") || (x!==undefined)) {
     throw "'this' had incorrect value!";
}
