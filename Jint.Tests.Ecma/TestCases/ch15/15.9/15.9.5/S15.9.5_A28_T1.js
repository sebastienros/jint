// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Date.prototype has the property "setMilliseconds"
 *
 * @path ch15/15.9/15.9.5/S15.9.5_A28_T1.js
 * @description The Date.prototype has the property "setMilliseconds"
 */

if(Date.prototype.hasOwnProperty("setMilliseconds") !== true){
  $ERROR('#1: The Date.prototype has the property "setMilliseconds"');
}


