// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Date.prototype has the property "toLocaleTimeString"
 *
 * @path ch15/15.9/15.9.5/S15.9.5_A07_T1.js
 * @description The Date.prototype has the property "toLocaleTimeString"
 */

if(Date.prototype.hasOwnProperty("toLocaleTimeString") !== true){
  $ERROR('#1: The Date.prototype has the property "toLocaleTimeString"');
}


