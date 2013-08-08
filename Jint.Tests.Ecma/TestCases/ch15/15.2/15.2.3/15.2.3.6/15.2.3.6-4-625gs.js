/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-625gs.js
 * @description Globally declared variable should take precedence over Object.prototype property of the same name
 */

Object.defineProperty(Object.prototype, 
                      "prop", 
                      { value: 1001, writable: false, enumerable: false, configurable: false} 
                      );
var prop = 1002;

if (! (this.hasOwnProperty("prop") && prop === 1002)) {
    throw "this.prop should take precedence over Object.prototype.prop";
}
